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
            NeedsNormalRoughness = true;
            AccessResolvedColor = true;
            AccessResolvedDepth = true;
            EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;
        }

        public override void _RenderCallback(int effectCallbackType, RenderData renderData)
        {
            if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD renderSceneBuffers) return;

            if (_rd == null)
            {
                _rd = RenderingServer.GetRenderingDevice();
                if (_rd == null)
                {
                    GD.PrintErr("Failed to get RenderingDevice - CompositorEffects require Forward+ or Mobile renderer");
                    return;
                }

                CreateSampler();
                if (!SetupComputeShader())
                {
                    GD.PrintErr("Failed to setup compute shader");
                    return;
                }
                CreateComputePipeline();
            }

            var colorBuffer = renderSceneBuffers.GetColorLayer(0);
            var depthBuffer = renderSceneBuffers.GetDepthLayer(0);

            // Check if normal/roughness buffer exists before trying to access it
            var normalRoughnessBuffer = renderSceneBuffers.GetTexture("forward_clustered", "normal_roughness");
            if (!normalRoughnessBuffer.IsValid)
            {
                GD.PrintErr("Normal/Roughness buffer not available - make sure you're using Forward+ renderer and NeedsNormalRoughness = true");
                return;
            }

            LightingPass(colorBuffer, depthBuffer, normalRoughnessBuffer);
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

        private bool SetupComputeShader()
        {
            var shaderFile = GD.Load<RDShaderFile>("res://shaders/lighting.glsl");
            if (shaderFile == null)
            {
                GD.PrintErr("Failed to load shader file");
                return false;
            }

            var shaderSpirv = shaderFile.GetSpirV();
            if (shaderSpirv == null)
            {
                GD.PrintErr("Failed to get SPIR-V from shader file. Check shader compilation errors.");
                if (!string.IsNullOrEmpty(shaderFile.BaseError))
                {
                    GD.PrintErr($"Shader base error: {shaderFile.BaseError}");
                }
                return false;
            }

            // Check for compilation errors
            if (!string.IsNullOrEmpty(shaderSpirv.CompileErrorCompute))
            {
                GD.PrintErr($"Compute shader compilation error: {shaderSpirv.CompileErrorCompute}");
                return false;
            }

            _computeShader = _rd.ShaderCreateFromSpirV(shaderSpirv);
            return _computeShader.IsValid;
        }

        private void CreateComputePipeline()
        {
            if (_computeShader.IsValid)
            {
                _computePipeline = _rd.ComputePipelineCreate(_computeShader);
            }
        }

        private void LightingPass(Rid colorTexture, Rid depthTexture, Rid normalRoughnessTexture)
        {
            if (!_computePipeline.IsValid) return;

            var uniforms = new Array<RDUniform>();

            // Color buffer as image (read/write)
            var colorUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.Image,
                Binding = 0
            };
            colorUniform.AddId(colorTexture);
            uniforms.Add(colorUniform);

            // Depth buffer as sampled texture (read-only)
            var depthUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 1
            };
            depthUniform.AddId(_sampler);
            depthUniform.AddId(depthTexture);
            uniforms.Add(depthUniform);

            // Normal/Roughness buffer as sampled texture (read-only)
            var normalRoughnessUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 2
            };
            normalRoughnessUniform.AddId(_sampler);
            normalRoughnessUniform.AddId(normalRoughnessTexture);
            uniforms.Add(normalRoughnessUniform);

            var uniformSet = _rd.UniformSetCreate(uniforms, _computeShader, 0);

            var textureFormat = _rd.TextureGetFormat(colorTexture);
            var xGroups = (textureFormat.Width + 15) / 16;
            var yGroups = (textureFormat.Height + 15) / 16;

            var computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _computePipeline);
            _rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _rd.ComputeListDispatch(computeList, xGroups, yGroups, 1);
            _rd.ComputeListEnd();
        }
    }
}
