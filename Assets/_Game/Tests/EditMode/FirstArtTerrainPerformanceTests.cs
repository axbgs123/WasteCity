using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainPerformanceTests
    {
        private const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/" +
            "Profiles/FirstArtTerrainProfile3D.asset";
        private const string PerformanceProbeTypeName =
            "WasteCity.Editor.GrayboxPerformanceProbe";
        private const int ExpandedWidth = 96;
        private const int ExpandedHeight = 64;
        private const int Seed = 8128;

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void PerformanceProbe_ExposesFirstArtTerrainEntryPoint()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);

            MethodInfo method = probe.GetMethod(
                "MeasureFirstArtTerrainPerformance",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);
        }

        [Test]
        public void PerformanceProbe_ExposesFrozenRuinsCliffContract()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);

            MethodInfo method = probe.GetMethod(
                "MeasureRuinsCliffPerformance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            Type result = probe.GetNestedType(
                "RuinsCliffPerformanceResult",
                BindingFlags.NonPublic);
            Assert.That(result, Is.Not.Null);
            AssertField<double[]>(result, "layoutAndBatchingMilliseconds");
            AssertField<double>(result, "layoutAndBatchingMedianMilliseconds");
            AssertField<double[]>(result, "totalInitializationMilliseconds");
            AssertField<double>(result, "totalInitializationMedianMilliseconds");
            AssertField<int>(result, "stableObservationCount");
            AssertField<long>(
                result,
                "managedAllocationBytesAcrossStableObservations");
            AssertField<int>(result, "rendererCount");
            AssertField<int>(result, "persistentObjectCount");
            AssertField<int>(result, "vertexCount");
            AssertField<int>(result, "triangleCount");
            AssertField<int>(result, "materialSlotCount");

            AssertConstant(probe, "RuinsCliffRunCount", 5);
            AssertConstant(
                probe,
                "RuinsCliffStableObservationCount",
                300);
            AssertConstant(
                probe,
                "RuinsCliffLayoutAndBatchingMaximumMedianMilliseconds",
                100d);
            AssertConstant(
                probe,
                "RuinsCliffTotalInitializationMaximumMedianMilliseconds",
                250d);
            AssertConstant(probe, "RuinsCliffMaximumRendererCount", 2);
            AssertConstant(probe, "RuinsCliffMaximumPersistentObjectCount", 3);
            AssertConstant(
                probe,
                "RuinsCliffMaximumMaterialSlotCount",
                13);
            AssertConstant(
                probe,
                "RuinsCliffResultEnvironmentVariable",
                "WASTECITY_RUINS_CLIFF_PERF_RESULT");
        }

        [Test]
        public void PerformanceProbe_ExposesTerrainRuntimeEvidenceAndProfilerMarker()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);

            MethodInfo method = probe.GetMethod(
                "RecordFirstArtTerrainRuntimeEvidence",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxPerformanceProbe.cs"));
            StringAssert.Contains("WASTECITY_FIRST_TERRAIN_RUNTIME_RESULT", source);
            StringAssert.Contains("compressedPayloadBytes", source);
            StringAssert.Contains("editorNativeMemoryBytes", source);
            StringAssert.Contains("editorObservedDuplicateDifferenceBytes", source);
            StringAssert.Contains("FirstArtTerrainRenderer3D", source);
            StringAssert.Contains("first-art-terrain-presenter", source);
        }

        [Test]
        public void ExpandedMap_UsesOneFormalRendererAndNoPerCellObjects()
        {
            TerrainPerformanceFixture fixture = CreateFixture();

            Assert.That(fixture.Presenter.IsPresented, Is.True);
            Assert.That(
                fixture.Presenter
                    .GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                fixture.Presenter
                    .GetComponentsInChildren<Transform>(true).Length,
                Is.LessThanOrEqualTo(2));
            Assert.That(
                fixture.Presenter.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(fixture.Presenter.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ExpandedMap_UsesExactFourPixelsPerCellControlDimensions()
        {
            TerrainPerformanceFixture fixture = CreateFixture();

            Assert.That(fixture.Presenter.ControlMaps, Is.Not.Null);
            Assert.That(fixture.Presenter.ControlMaps.Width, Is.EqualTo(384));
            Assert.That(fixture.Presenter.ControlMaps.Height, Is.EqualTo(256));
            Assert.That(
                fixture.Presenter.ControlMaps.ControlA.width,
                Is.EqualTo(384));
            Assert.That(
                fixture.Presenter.ControlMaps.ControlB.height,
                Is.EqualTo(256));
        }

        [Test]
        public void StablePresenterObservation_AllocatesNoManagedBytesAcross300Calls()
        {
            TerrainPerformanceFixture fixture = CreateFixture();
            int warmup = ObservePresenter(fixture.Presenter);
            warmup += ObservePresenter(fixture.Presenter);

            ProfilerRecorder recorder = StartGcRecorder();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int observable = 0;
            for (int frame = 0; frame < 300; frame++)
                observable += ObservePresenter(fixture.Presenter);
            long currentThreadBytes =
                GC.GetAllocatedBytesForCurrentThread() - before;
            AllocationResult result = StopAndRead(recorder);

            TestContext.WriteLine(
                "FirstTerrainStableObservable=" + observable);
            TestContext.WriteLine(
                "FirstTerrainStableThreadBytes=" + currentThreadBytes);
            TestContext.WriteLine(
                "FirstTerrainStableProfiledSamples=" + result.Samples);
            TestContext.WriteLine(
                "FirstTerrainStableProfiledBytes=" + result.Bytes);
            Assert.That(warmup, Is.EqualTo(1406));
            Assert.That(observable, Is.EqualTo(210900));
            Assert.That(currentThreadBytes, Is.Zero);
            Assert.That(result.Bytes, Is.Zero);
        }

        [Test]
        public void WaterMotion_IsShaderDrivenWithoutPresenterUpdateOrCpuSamples()
        {
            TerrainPerformanceFixture fixture = CreateFixture();
            MethodInfo update = typeof(FirstArtTerrainRenderer3D).GetMethod(
                "Update",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts,
                "FirstArtTerrainRenderer3D.Update()",
                512,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            int observable = 0;
            for (int frame = 0; frame < 300; frame++)
                observable += ObservePresenter(fixture.Presenter);
            recorder.Stop();
            int samples = recorder.Count;
            recorder.Dispose();

            TestContext.WriteLine(
                "FirstTerrainWaterCpuUpdateSamples=" + samples);
            Assert.That(observable, Is.EqualTo(210900));
            Assert.That(update, Is.Null);
            Assert.That(samples, Is.Zero);
        }

        [Test]
        public void ProfilerRecorderPositiveControlCapturesDeliberateAllocation()
        {
            var retained = new object[64];
            ProfilerRecorder recorder = StartGcRecorder();
            int checksum = AllocateAndRetain(retained);
            GC.KeepAlive(retained);
            AllocationResult result = StopAndRead(recorder);

            TestContext.WriteLine(
                "FirstTerrainPositiveControlSamples=" + result.Samples);
            TestContext.WriteLine(
                "FirstTerrainPositiveControlBytes=" + result.Bytes);
            Assert.That(checksum, Is.EqualTo(69632));
            Assert.That((byte[])retained[0], Has.Length.EqualTo(1024));
            Assert.That((byte[])retained[63], Has.Length.EqualTo(1087));
            Assert.That(result.Samples, Is.GreaterThan(0));
            Assert.That(result.Bytes, Is.GreaterThan(0));
        }

        private TerrainPerformanceFixture CreateFixture()
        {
            var root = Track(new GameObject("FirstTerrainPerformanceWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Shader fallbackShader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(fallbackShader, Is.Not.Null);
            Material fallbackMaterial = Track(new Material(fallbackShader));
            GrayboxWorldView3D world =
                root.AddComponent<GrayboxWorldView3D>();
            world.Configure(
                terrain,
                resources,
                obstacles,
                fallbackMaterial);
            world.Generate(
                new WorldMapModel(
                    ExpandedWidth,
                    ExpandedHeight,
                    new WorldSeed(Seed)));

            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                    ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(
                profile.TryValidate(out string validationError),
                Is.True,
                validationError);
            var presenterObject =
                Track(new GameObject("FirstArtTerrainRenderer"));
            FirstArtTerrainRenderer3D presenter =
                presenterObject.AddComponent<FirstArtTerrainRenderer3D>();
            presenter.runInEditMode = true;
            presenter.Configure(profile);
            Assert.That(presenter.TryPresent(world), Is.True);
            return new TerrainPerformanceFixture(world, presenter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ObservePresenter(
            FirstArtTerrainRenderer3D presenter)
        {
            int value = presenter.IsPresented ? 1 : 0;
            value += presenter.SurfaceRenderer != null ? 2 : 0;
            value += presenter.SurfaceRenderer.enabled ? 4 : 0;
            value += presenter.ControlMaps != null ? 8 : 0;
            value += presenter.ControlMaps.Width;
            value += presenter.ControlMaps.Height;
            value += presenter.Profile != null ? 16 : 0;
            value += string.IsNullOrEmpty(presenter.LastPresentationError)
                ? 32
                : 0;
            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int AllocateAndRetain(object[] retained)
        {
            int checksum = 0;
            for (int index = 0; index < retained.Length; index++)
            {
                var allocation = new byte[1024 + index];
                allocation[0] = (byte)(index + 1);
                retained[index] = allocation;
                checksum += allocation.Length + allocation[0];
            }
            return checksum;
        }

        private static ProfilerRecorder StartGcRecorder()
        {
            return ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                2048,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
        }

        private static AllocationResult StopAndRead(
            ProfilerRecorder recorder)
        {
            recorder.Stop();
            int samples = recorder.Count;
            long bytes = 0;
            for (int index = 0; index < recorder.Count; index++)
            {
                ProfilerRecorderSample sample = recorder.GetSample(index);
                bytes += sample.Value * sample.Count;
            }
            recorder.Dispose();
            return new AllocationResult(samples, bytes);
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static void AssertField<T>(Type owner, string name)
        {
            FieldInfo field = owner?.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(T)), name);
        }

        private static void AssertConstant<T>(
            Type owner,
            string name,
            T expected)
        {
            FieldInfo field = owner?.GetField(
                name,
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field?.IsLiteral, Is.True, name);
            Assert.That(field?.GetRawConstantValue(), Is.EqualTo(expected), name);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private readonly struct TerrainPerformanceFixture
        {
            public TerrainPerformanceFixture(
                GrayboxWorldView3D world,
                FirstArtTerrainRenderer3D presenter)
            {
                World = world;
                Presenter = presenter;
            }

            public GrayboxWorldView3D World { get; }
            public FirstArtTerrainRenderer3D Presenter { get; }
        }

        private readonly struct AllocationResult
        {
            public AllocationResult(int samples, long bytes)
            {
                Samples = samples;
                Bytes = bytes;
            }

            public int Samples { get; }
            public long Bytes { get; }
        }
    }
}
