using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WasteCity.Graybox3D;

namespace WasteCity.Tests
{
    public sealed class GrayboxUrpScopeTests
    {
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
        public void Enter_SetsGraphicsAndQualityToGrayboxPipeline()
        {
            UniversalRenderPipelineAsset graybox = NewPipeline();
            GrayboxUrpScope scope = NewDisabledScope(graybox);

            Assert.That(scope.Enter(), Is.True);

            Assert.That(scope.IsApplied, Is.True);
            Assert.That(scope.PipelineAsset, Is.SameAs(graybox));
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(graybox));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(graybox));
        }

        [Test]
        public void Exit_RestoresBothPropertiesWhenStillOwned()
        {
            RenderPipelineAsset oldGraphics =
                GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality =
                QualitySettings.renderPipeline;
            GrayboxUrpScope scope = NewDisabledScope(NewPipeline());
            scope.Enter();

            scope.Exit();

            Assert.That(scope.IsApplied, Is.False);
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(oldGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(oldQuality));
        }

        [Test]
        public void Exit_PreservesExternalGraphicsChangeAndRestoresOwnedQuality()
        {
            RenderPipelineAsset oldQuality =
                QualitySettings.renderPipeline;
            UniversalRenderPipelineAsset graybox = NewPipeline();
            UniversalRenderPipelineAsset external = NewPipeline();
            GrayboxUrpScope scope = NewDisabledScope(graybox);
            scope.Enter();
            GraphicsSettings.defaultRenderPipeline = external;

            scope.Exit();

            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(external));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(oldQuality));
        }

        [Test]
        public void Exit_PreservesExternalQualityChangeAndRestoresOwnedGraphics()
        {
            RenderPipelineAsset oldGraphics =
                GraphicsSettings.defaultRenderPipeline;
            UniversalRenderPipelineAsset graybox = NewPipeline();
            UniversalRenderPipelineAsset external = NewPipeline();
            GrayboxUrpScope scope = NewDisabledScope(graybox);
            scope.Enter();
            QualitySettings.renderPipeline = external;

            scope.Exit();

            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(external));
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(oldGraphics));
        }

        [Test]
        public void Enter_WithoutPipelineLeavesBothPropertiesUnchanged()
        {
            RenderPipelineAsset oldGraphics =
                GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality =
                QualitySettings.renderPipeline;
            GrayboxUrpScope scope = NewDisabledScope(null);

            Assert.That(scope.Enter(), Is.False);

            Assert.That(scope.IsApplied, Is.False);
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(oldGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(oldQuality));
        }

        [Test]
        public void Enter_AllowsOnlyOneOwnerUntilFirstScopeExits()
        {
            GrayboxUrpScope first = NewDisabledScope(NewPipeline());
            GrayboxUrpScope second = NewDisabledScope(NewPipeline());

            Assert.That(first.Enter(), Is.True);
            Assert.That(second.Enter(), Is.False);
            first.Exit();
            Assert.That(second.Enter(), Is.True);
        }

        [Test]
        public void Enter_IsIdempotentAndRestoresOriginalCapture()
        {
            RenderPipelineAsset oldGraphics =
                GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset oldQuality =
                QualitySettings.renderPipeline;
            GrayboxUrpScope scope = NewDisabledScope(NewPipeline());

            Assert.That(scope.Enter(), Is.True);
            Assert.That(scope.Enter(), Is.True);
            scope.Exit();

            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(oldGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(oldQuality));
        }

        [Test]
        public void EnableAndDisable_EnterAndExitScope()
        {
            UniversalRenderPipelineAsset graybox = NewPipeline();
            GrayboxUrpScope scope = NewDisabledScope(graybox);
            GameObject owner = scope.gameObject;
            scope.runInEditMode = true;

            owner.SetActive(true);
            Assert.That(scope.IsApplied, Is.True);
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(graybox));

            owner.SetActive(false);
            Assert.That(scope.IsApplied, Is.False);
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(originalGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(originalQuality));
        }

        private UniversalRenderPipelineAsset NewPipeline()
        {
            UniversalRenderPipelineAsset pipeline =
                ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            cleanup.Add(pipeline);
            return pipeline;
        }

        private GrayboxUrpScope NewDisabledScope(
            UniversalRenderPipelineAsset pipeline)
        {
            var owner = new GameObject("GrayboxUrpScope");
            owner.SetActive(false);
            cleanup.Add(owner);
            GrayboxUrpScope scope = owner.AddComponent<GrayboxUrpScope>();
            scope.Configure(pipeline);
            return scope;
        }
    }
}
