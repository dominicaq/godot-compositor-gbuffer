using Godot;
using Godot.Collections;

namespace DeferredRenderer
{
    [GlobalClass]
    public partial class DeferredCompositor : CompositorEffect
    {
        private RenderingDevice _rd;
        private Rid _computeShader;
        private Rid _computePipeline;
        private Rid _sampler;

        public DeferredCompositor()
        {
            AccessResolvedColor = true;
            AccessResolvedDepth = true;
            EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;
        }

        public override void _RenderCallback(int effectCallbackType, RenderData renderData)
        {
            if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD renderBuffersRD) return;

            if (_rd == null)
            {
                _rd = RenderingServer.GetRenderingDevice();
                CreateSampler();
                SetupDepthShader();
                CreateComputePipeline();
            }

            var colorTexture = renderBuffersRD.GetColorLayer(0);
            var depthTexture = renderBuffersRD.GetDepthLayer(0);

            DepthPass(colorTexture, depthTexture);
        }

        private void CreateSampler()
        {
            var samplerState = new RDSamplerState
            {
                MagFilter = RenderingDevice.SamplerFilter.Linear,
                MinFilter = RenderingDevice.SamplerFilter.Linear,
                RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
                RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge
            };
            _sampler = _rd.SamplerCreate(samplerState);
        }

        private void SetupDepthShader()
        {
            var shaderFile = GD.Load<RDShaderFile>("res://shaders/lighting.glsl");
            var shaderSpirv = shaderFile.GetSpirV();
            _computeShader = _rd.ShaderCreateFromSpirV(shaderSpirv);
        }

        private void CreateComputePipeline()
        {
            if (_computeShader.IsValid)
            {
                _computePipeline = _rd.ComputePipelineCreate(_computeShader);
            }
        }

        private void DepthPass(Rid colorTexture, Rid depthTexture)
        {
            if (!_computePipeline.IsValid) return;

            var uniforms = new Array<RDUniform>();

            var colorUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.Image,
                Binding = 0
            };
            colorUniform.AddId(colorTexture);
            uniforms.Add(colorUniform);

            var depthUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 1
            };
            depthUniform.AddId(_sampler);
            depthUniform.AddId(depthTexture);
            uniforms.Add(depthUniform);

            var uniformSet = _rd.UniformSetCreate(uniforms, _computeShader, 0);

            var textureFormat = _rd.TextureGetFormat(colorTexture);
            var xGroups = (textureFormat.Width + 15) / 16;
            var yGroups = (textureFormat.Height + 15) / 16;

            var computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _computePipeline);
            _rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _rd.ComputeListDispatch(computeList, xGroups, yGroups, 1);
            _rd.ComputeListEnd();
            // REMOVED: _rd.Submit(); - Godot handles this automatically
        }
    }
}
