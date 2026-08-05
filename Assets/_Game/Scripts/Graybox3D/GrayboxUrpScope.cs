using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WasteCity.Graybox3D
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GrayboxUrpScope : MonoBehaviour
    {
        private static GrayboxUrpScope activeOwner;

        [SerializeField]
        private UniversalRenderPipelineAsset pipelineAsset;

        private RenderPipelineAsset previousGraphics;
        private RenderPipelineAsset previousQuality;

        public UniversalRenderPipelineAsset PipelineAsset => pipelineAsset;
        public bool IsApplied { get; private set; }

        public void Configure(UniversalRenderPipelineAsset pipelineAsset)
        {
            this.pipelineAsset = pipelineAsset;
        }

        public bool Enter()
        {
            if (pipelineAsset == null ||
                (activeOwner != null && activeOwner != this))
                return false;
            if (IsApplied)
                return true;

            previousGraphics = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            activeOwner = this;
            IsApplied = true;
            return true;
        }

        public void Exit()
        {
            if (!IsApplied)
                return;

            if (QualitySettings.renderPipeline == pipelineAsset)
                QualitySettings.renderPipeline = previousQuality;
            if (GraphicsSettings.defaultRenderPipeline == pipelineAsset)
                GraphicsSettings.defaultRenderPipeline = previousGraphics;
            if (activeOwner == this)
                activeOwner = null;

            IsApplied = false;
            previousGraphics = null;
            previousQuality = null;
        }

        private void OnEnable()
        {
            Enter();
        }

        private void OnDisable()
        {
            Exit();
        }

        private void OnDestroy()
        {
            Exit();
        }
    }
}
