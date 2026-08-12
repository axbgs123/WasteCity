using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class RecordingTerrainPresentation3D : MonoBehaviour,
        IGrayboxTerrainPresentation3D
    {
        public bool Result { get; set; } = true;
        public bool ThrowOnPresent { get; set; }
        public bool ThrowOnClear { get; set; }
        public bool CreatePartialOwnership { get; set; }
        public bool ParticipateInWorldLifecycle { get; set; }
        public bool HideFallbackBeforeFailure { get; set; }
        public bool DetachOnClear { get; set; } = true;
        public bool HideFallbackDuringClear { get; set; }
        public bool ThrowBeforeReleaseOnClear { get; set; }
        public int ThrowOnClearCall { get; set; }
        public int TryPresentCalls { get; private set; }
        public int ClearPresentationCalls { get; private set; }
        public bool SawModel { get; private set; }
        public bool SawCoordinates { get; private set; }
        public bool FallbackVisibleAtLastTry { get; private set; }
        public GameObject OwnedObject { get; private set; }
        public Mesh OwnedMesh { get; private set; }

        private GrayboxWorldView3D retainedWorldView;

        public bool TryPresent(GrayboxWorldView3D worldView)
        {
            TryPresentCalls++;
            SawModel = worldView != null && worldView.Model != null;
            SawCoordinates =
                worldView != null && worldView.Coordinates != null;
            FallbackVisibleAtLastTry =
                worldView != null && worldView.SurfaceFallbackVisible;
            if (CreatePartialOwnership)
                CreateOwnedResources();
            if (ParticipateInWorldLifecycle &&
                (Result || HideFallbackBeforeFailure))
            {
                retainedWorldView = worldView;
                retainedWorldView.AttachTerrainPresentation(this);
                retainedWorldView.SetSurfaceFallbackVisible(false);
            }
            if (ThrowOnPresent)
                throw new InvalidOperationException(
                    "Injected presenter failure.");
            return Result;
        }

        public void ClearPresentation()
        {
            ClearPresentationCalls++;
            bool shouldThrow = ThrowOnClear &&
                (ThrowOnClearCall <= 0 ||
                 ThrowOnClearCall == ClearPresentationCalls);
            if (retainedWorldView != null && HideFallbackDuringClear)
                retainedWorldView.SetSurfaceFallbackVisible(false);
            if (shouldThrow && ThrowBeforeReleaseOnClear)
            {
                if (retainedWorldView != null && DetachOnClear)
                {
                    GrayboxWorldView3D viewToDetach = retainedWorldView;
                    retainedWorldView = null;
                    viewToDetach.DetachTerrainPresentation(this);
                }
                throw new InvalidOperationException(
                    "Injected presenter cleanup failure.");
            }

            ReleaseOwnedResources();
            if (retainedWorldView != null && DetachOnClear)
            {
                GrayboxWorldView3D viewToDetach = retainedWorldView;
                retainedWorldView = null;
                viewToDetach.DetachTerrainPresentation(this);
                viewToDetach.SetSurfaceFallbackVisible(true);
            }
            if (shouldThrow)
            {
                throw new InvalidOperationException(
                    "Injected presenter cleanup failure.");
            }
        }

        private void CreateOwnedResources()
        {
            ReleaseOwnedResources();
            OwnedObject = new GameObject("PartialTerrainPresentation");
            OwnedObject.transform.SetParent(transform, false);
            OwnedMesh = new Mesh { name = "partial.terrain.mesh" };
            OwnedObject.AddComponent<MeshFilter>().sharedMesh = OwnedMesh;
        }

        private void ReleaseOwnedResources()
        {
            if (OwnedObject != null)
                UnityEngine.Object.DestroyImmediate(OwnedObject);
            if (OwnedMesh != null)
                UnityEngine.Object.DestroyImmediate(OwnedMesh);
        }

        private void OnDestroy()
        {
            ReleaseOwnedResources();
            if (retainedWorldView != null)
                retainedWorldView.DetachTerrainPresentation(this);
        }
    }

    public sealed class GrayboxSceneBootstrapTests
    {
        private const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/" +
            "Profiles/FirstArtTerrainProfile3D.asset";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private RenderPipelineAsset originalGraphics;
        private RenderPipelineAsset originalQuality;

        [SetUp]
        public void SetUp()
        {
            originalGraphics = GraphicsSettings.defaultRenderPipeline;
            originalQuality = QualitySettings.renderPipeline;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GrayboxUrpScope scope in
                     UnityEngine.Object.FindObjectsOfType<GrayboxUrpScope>(
                         true))
                scope.Exit();

            GraphicsSettings.defaultRenderPipeline = originalGraphics;
            QualitySettings.renderPipeline = originalQuality;

            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void Initialize_WithoutAppliedScope_DoesNotGenerateWorld()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);

            Assert.That(bootstrap.Initialize(), Is.False);

            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.World, Is.Null);
            Assert.That(view.Model, Is.Null);
        }

        [Test]
        public void Initialize_GeneratesFrozenWorldAndView()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);
            Assert.That(scope.Enter(), Is.True);

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.World, Is.SameAs(view.Model));
            Assert.That(bootstrap.World.Width, Is.EqualTo(64));
            Assert.That(bootstrap.World.Height, Is.EqualTo(48));
            AssertWorldEquals(
                GrayboxWorldLayout3D.CreateDefault(),
                bootstrap.World);
        }

        [Test]
        public void Initialize_WhenCalledTwice_PreservesGeneratedObjects()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            WorldMapModel firstWorld = bootstrap.World;
            GrayboxVisualSlot[] firstSlots =
                view.GetComponentsInChildren<GrayboxVisualSlot>(true);

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.World, Is.SameAs(firstWorld));
            Assert.That(
                view.GetComponentsInChildren<GrayboxVisualSlot>(true),
                Is.EqualTo(firstSlots));
        }

        [Test]
        public void Initialize_WithPresenter_CallsItOnceAfterWorldGeneration()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);

            Assert.That(bootstrap.Initialize(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(presenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(presenter.SawModel, Is.True);
            Assert.That(presenter.SawCoordinates, Is.True);
            Assert.That(bootstrap.World, Is.SameAs(view.Model));
        }

        [Test]
        public void Initialize_WhenPresenterReturnsFalse_RestoresFallbackAndInitializes()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            view.SetSurfaceFallbackVisible(false);
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.Result = false;
            presenter.CreatePartialOwnership = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Graybox terrain presentation returned false"));

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(presenter.OwnedObject == null, Is.True);
            Assert.That(presenter.OwnedMesh == null, Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                AllGeneratedRenderersEnabled(view),
                Is.True);
        }

        [Test]
        public void Initialize_WhenPresenterThrows_RestoresFallbackAndInitializes()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            view.SetSurfaceFallbackVisible(false);
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ThrowOnPresent = true;
            presenter.CreatePartialOwnership = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Graybox terrain presentation failed: " +
                          "Injected presenter failure"));

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(presenter.OwnedObject == null, Is.True);
            Assert.That(presenter.OwnedMesh == null, Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                AllGeneratedRenderersEnabled(view),
                Is.True);
        }

        [Test]
        public void Initialize_WhenPresentationAndCleanupFail_LogsOnceAndRestoresFallback()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            view.SetSurfaceFallbackVisible(false);
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.Result = false;
            presenter.CreatePartialOwnership = true;
            presenter.ThrowOnClear = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "terrain presentation returned false.*" +
                    "cleanup failed: Injected presenter cleanup failure",
                    RegexOptions.Singleline));

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(presenter.OwnedObject == null, Is.True);
            Assert.That(presenter.OwnedMesh == null, Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Initialize_WithFailingRealPresenter_LogsExactlyOnce()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            FirstArtTerrainRenderer3D presenter = NewRealPresenter(null);
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "terrain presentation failed: Terrain profile is required"));

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Configure_WhenDependenciesChange_ClearsOldAndAllowsNewInitialization()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D firstView = NewWorldView();
            RecordingTerrainPresentation3D firstPresenter = NewPresenter();
            firstPresenter.CreatePartialOwnership = true;
            firstPresenter.ParticipateInWorldLifecycle = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, firstView, firstPresenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            Assert.That(firstPresenter.OwnedObject, Is.Not.Null);
            GrayboxWorldView3D secondView = NewWorldView();
            RecordingTerrainPresentation3D secondPresenter = NewPresenter();

            bootstrap.Configure(scope, secondView, secondPresenter);

            Assert.That(firstPresenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(firstPresenter.OwnedObject == null, Is.True);
            Assert.That(firstPresenter.OwnedMesh == null, Is.True);
            Assert.That(firstView.SurfaceFallbackVisible, Is.True);
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.World, Is.Null);

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(secondPresenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(bootstrap.World, Is.SameAs(secondView.Model));
            Assert.That(bootstrap.IsInitialized, Is.True);
        }

        [Test]
        public void Configure_WithSameLiveDependencies_RemainsIdempotent()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            presenter.CreatePartialOwnership = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            WorldMapModel initializedWorld = bootstrap.World;

            bootstrap.Configure(scope, view, presenter);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.World, Is.SameAs(initializedWorld));
            Assert.That(presenter.ClearPresentationCalls, Is.Zero);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.False);
        }

        [Test]
        public void Configure_DestroyedDependenciesAndExplicitNull_ResetState()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            UnityEngine.Object.DestroyImmediate(presenter.gameObject);
            UnityEngine.Object.DestroyImmediate(view.gameObject);

            bootstrap.Configure(scope, null, null);

            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.World, Is.Null);
            Assert.That(bootstrap.Initialize(), Is.False);
        }

        [Test]
        public void Configure_ReplacingRealPresenterPreventsOldSourceResurrection()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D firstView = NewWorldView();
            FirstArtTerrainRenderer3D firstPresenter =
                NewRealPresenter(LoadApprovedProfile());
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, firstView, firstPresenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            Assert.That(firstPresenter.IsPresented, Is.True);
            GrayboxWorldView3D secondView = NewWorldView();
            RecordingTerrainPresentation3D secondPresenter = NewPresenter();

            bootstrap.Configure(scope, secondView, secondPresenter);
            firstPresenter.enabled = false;
            firstPresenter.enabled = true;

            Assert.That(firstPresenter.IsPresented, Is.False);
            Assert.That(firstView.SurfaceFallbackVisible, Is.True);
        }

        [Test]
        public void ExternalGenerate_ClearsBeforeReplacementAndRepresentsOnceAfterSuccess()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(1));
            Assert.That(view.SurfaceFallbackVisible, Is.False);

            WorldMapModel replacement = new WorldMapModel(
                4,
                3,
                new WorldSeed(104729));
            view.Generate(replacement);

            Assert.That(view.Model, Is.SameAs(replacement));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(1));
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(2));
            Assert.That(presenter.FallbackVisibleAtLastTry, Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.False);
        }

        [Test]
        public void ExternalGenerate_WhenPreRebuildCleanupThrows_RetainsHandleForRetry()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            presenter.CreatePartialOwnership = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            WorldMapModel originalModel = view.Model;
            int originalRendererCount = view.WorldRendererCount;
            GameObject oldObject = presenter.OwnedObject;
            Mesh oldMesh = presenter.OwnedMesh;
            presenter.ThrowOnClear = true;
            presenter.ThrowOnClearCall = 1;
            presenter.ThrowBeforeReleaseOnClear = true;
            presenter.HideFallbackDuringClear = true;
            WorldMapModel replacement = new WorldMapModel(
                4,
                3,
                new WorldSeed(104729));

            Assert.That(
                () => view.Generate(replacement),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo(
                        "Injected presenter cleanup failure."));

            Assert.That(view.Model, Is.SameAs(originalModel));
            Assert.That(
                view.WorldRendererCount,
                Is.EqualTo(originalRendererCount));
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.True);
            Assert.That(oldObject, Is.Not.Null);
            Assert.That(oldMesh, Is.Not.Null);

            presenter.ThrowOnClear = false;
            presenter.HideFallbackDuringClear = false;
            Assert.DoesNotThrow(() => view.Generate(replacement));

            Assert.That(view.Model, Is.SameAs(replacement));
            Assert.That(oldObject == null, Is.True);
            Assert.That(oldMesh == null, Is.True);
            Assert.That(presenter.TryPresentCalls, Is.EqualTo(2));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(2));
            Assert.That(
                presenter.GetComponentsInChildren<MeshFilter>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExternalGenerate_WhenRepresentationReturnsFalse_CleansAndRestoresFallback()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            presenter.Result = false;
            presenter.HideFallbackBeforeFailure = true;
            presenter.DetachOnClear = false;

            Assert.DoesNotThrow(
                () => view.Generate(new WorldMapModel(
                    4,
                    3,
                    new WorldSeed(104729))));

            Assert.That(presenter.TryPresentCalls, Is.EqualTo(2));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(2));
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExternalGenerate_WhenRepresentationThrows_CleansAndRestoresFallbackBeforeRethrow()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            presenter.ThrowOnPresent = true;
            presenter.HideFallbackBeforeFailure = true;
            presenter.DetachOnClear = false;

            Assert.That(
                () => view.Generate(new WorldMapModel(
                    4,
                    3,
                    new WorldSeed(104729))),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo("Injected presenter failure."));

            Assert.That(presenter.TryPresentCalls, Is.EqualTo(2));
            Assert.That(presenter.ClearPresentationCalls, Is.EqualTo(2));
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExternalGenerate_WhenRepresentationReturnsFalseAndCleanupThrows_PreservesCleanupError()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            presenter.CreatePartialOwnership = true;
            presenter.DetachOnClear = false;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            presenter.Result = false;
            presenter.HideFallbackBeforeFailure = true;
            presenter.ThrowOnClear = true;
            presenter.ThrowOnClearCall = 2;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () => view.Generate(new WorldMapModel(
                        4,
                        3,
                        new WorldSeed(104729))));

            Assert.That(
                exception.Message,
                Is.EqualTo(
                    "Terrain presentation returned false and cleanup " +
                    "failed."));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(
                exception.InnerException.Message,
                Is.EqualTo("Injected presenter cleanup failure."));
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.False);
            Assert.That(presenter.OwnedObject == null, Is.True);
            Assert.That(presenter.OwnedMesh == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExternalGenerate_WhenRepresentationAndCleanupThrow_AggregatesInAttemptFirstOrder()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            RecordingTerrainPresentation3D presenter = NewPresenter();
            presenter.ParticipateInWorldLifecycle = true;
            presenter.CreatePartialOwnership = true;
            presenter.DetachOnClear = false;
            GrayboxSceneBootstrap bootstrap =
                NewBootstrap(scope, view, presenter);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            presenter.ThrowOnPresent = true;
            presenter.HideFallbackBeforeFailure = true;
            presenter.ThrowOnClear = true;
            presenter.ThrowOnClearCall = 2;

            AggregateException exception =
                Assert.Throws<AggregateException>(
                    () => view.Generate(new WorldMapModel(
                        4,
                        3,
                        new WorldSeed(104729))));

            Assert.That(exception.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(
                exception.InnerExceptions[0].Message,
                Is.EqualTo("Injected presenter failure."));
            Assert.That(
                exception.InnerExceptions[1].Message,
                Is.EqualTo("Injected presenter cleanup failure."));
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                view.IsTerrainPresentationActive(presenter),
                Is.False);
            Assert.That(presenter.OwnedObject == null, Is.True);
            Assert.That(presenter.OwnedMesh == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Initialize_WithMissingAssetOrView_DoesNotGenerateWorld()
        {
            GrayboxUrpScope missingAssetScope = NewScope(null);
            GrayboxWorldView3D unusedView = NewWorldView();
            GrayboxSceneBootstrap missingAsset =
                NewBootstrap(missingAssetScope, unusedView);
            Assert.That(missingAssetScope.Enter(), Is.False);

            Assert.That(missingAsset.Initialize(), Is.False);
            Assert.That(missingAsset.World, Is.Null);
            Assert.That(unusedView.Model, Is.Null);

            GrayboxUrpScope validScope = NewScope(NewPipeline());
            Assert.That(validScope.Enter(), Is.True);
            GrayboxSceneBootstrap missingView =
                NewBootstrap(validScope, null);

            Assert.That(missingView.Initialize(), Is.False);
            Assert.That(missingView.World, Is.Null);
        }

        private GrayboxUrpScope NewScope(
            UniversalRenderPipelineAsset pipeline)
        {
            var owner = Track(new GameObject("GrayboxUrpScope"));
            owner.SetActive(false);
            GrayboxUrpScope scope = owner.AddComponent<GrayboxUrpScope>();
            scope.Configure(pipeline);
            return scope;
        }

        private UniversalRenderPipelineAsset NewPipeline()
        {
            return Track(
                ScriptableObject.CreateInstance<
                    UniversalRenderPipelineAsset>());
        }

        private GrayboxWorldView3D NewWorldView()
        {
            var root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
        }

        private GrayboxSceneBootstrap NewBootstrap(
            GrayboxUrpScope scope,
            GrayboxWorldView3D view)
        {
            var owner = Track(new GameObject("GrayboxSceneBootstrap"));
            owner.SetActive(false);
            GrayboxSceneBootstrap bootstrap =
                owner.AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(scope, view);
            return bootstrap;
        }

        private GrayboxSceneBootstrap NewBootstrap(
            GrayboxUrpScope scope,
            GrayboxWorldView3D view,
            MonoBehaviour presenter)
        {
            var owner = Track(new GameObject("GrayboxSceneBootstrap"));
            owner.SetActive(false);
            GrayboxSceneBootstrap bootstrap =
                owner.AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(scope, view, presenter);
            return bootstrap;
        }

        private RecordingTerrainPresentation3D NewPresenter()
        {
            var owner = Track(new GameObject("TerrainPresenter"));
            return owner.AddComponent<RecordingTerrainPresentation3D>();
        }

        private FirstArtTerrainRenderer3D NewRealPresenter(
            FirstArtTerrainProfile3D profile)
        {
            var owner = Track(new GameObject("FirstArtTerrainRenderer"));
            FirstArtTerrainRenderer3D presenter =
                owner.AddComponent<FirstArtTerrainRenderer3D>();
            presenter.runInEditMode = true;
            presenter.Configure(profile);
            return presenter;
        }

        private static FirstArtTerrainProfile3D LoadApprovedProfile()
        {
            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                    ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(
                profile.TryValidate(out string error),
                Is.True,
                error);
            return profile;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void AssertWorldEquals(
            WorldMapModel expected,
            WorldMapModel actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            for (int x = 0; x < expected.Width; x++)
            for (int y = 0; y < expected.Height; y++)
            {
                WorldCell expectedCell = expected.Get(x, y);
                WorldCell actualCell = actual.Get(x, y);
                Assert.That(actualCell.Terrain, Is.EqualTo(expectedCell.Terrain));
                Assert.That(
                    actualCell.ResourceId,
                    Is.EqualTo(expectedCell.ResourceId));
                Assert.That(
                    actualCell.ResourceAmount,
                    Is.EqualTo(expectedCell.ResourceAmount));
                Assert.That(
                    actualCell.Traversal,
                    Is.EqualTo(expectedCell.Traversal));
            }
        }

        private static bool AllGeneratedRenderersEnabled(
            GrayboxWorldView3D view)
        {
            foreach (GrayboxVisualSlot slot in
                     view.GetComponentsInChildren<GrayboxVisualSlot>(true))
            {
                if (!slot.Renderer.enabled)
                    return false;
            }
            return true;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
