using Godot;
using Godot.Collections;

namespace DeferredRenderer
{
    [GlobalClass]
    public partial class RayTracer : CompositorEffect
    {
        private Rid _computeShader;
        private Rid _computePipeline;
        private Rid _sampler;
        private RenderingDevice _rd;

        // Reference to the GBuffer builder
        private GBufferBuilder _gBufferBuilder;

        public RayTracer()
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

            // Set this pass to run after the GBuffer pass
            EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
        }

        // Method to set the GBuffer reference (call this after creating both effects)
        public void SetGBufferBuilder(GBufferBuilder gBufferBuilder)
        {
            _gBufferBuilder = gBufferBuilder;
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

        public override void _RenderCallback(int effectCallbackType, RenderData renderData)
        {
            if (_gBufferBuilder?.GBuffer == null)
            {
                GD.PrintErr("GBuffer not available - make sure GBufferBuilder is set and runs before RayTracer");
                return;
            }

            // Get the G-Buffer textures from the builder
            var worldPosTexture = _gBufferBuilder.GBuffer[(int)GBufferBuilder.GBufferIndex.WorldPosition];
            var signBitsTexture = _gBufferBuilder.GBuffer[(int)GBufferBuilder.GBufferIndex.SignBits];
            var normalRoughnessTexture = _gBufferBuilder.GBuffer[(int)GBufferBuilder.GBufferIndex.NormalRoughness];
            var depthTexture = _gBufferBuilder.GBuffer[(int)GBufferBuilder.GBufferIndex.Depth];

            // Validate that we have valid textures
            if (!worldPosTexture.IsValid || !normalRoughnessTexture.IsValid ||
                !signBitsTexture.IsValid || !depthTexture.IsValid)
            {
                GD.PrintErr("One or more G-Buffer textures are invalid");
                return;
            }

            RayTracingPass(worldPosTexture, signBitsTexture, normalRoughnessTexture, depthTexture);
        }

        private void RayTracingPass(Rid worldPosTexture, Rid signBitsTexture, Rid normalRoughnessTexture, Rid depthTexture)
        {
            if (!_computePipeline.IsValid) return;

            var uniforms = new Array<RDUniform>();

            // Bind world position as image (for read/write)
            var worldPosUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.Image,
                Binding = 0
            };
            worldPosUniform.AddId(worldPosTexture);
            uniforms.Add(worldPosUniform);

            // Bind sign bits (specular) texture with sampler
            var signBitsUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 1
            };
            signBitsUniform.AddId(_sampler);
            signBitsUniform.AddId(signBitsTexture);
            uniforms.Add(signBitsUniform);

            // Bind normal/roughness texture with sampler
            var normalRoughnessUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 2
            };
            normalRoughnessUniform.AddId(_sampler);
            normalRoughnessUniform.AddId(normalRoughnessTexture);
            uniforms.Add(normalRoughnessUniform);

            // Bind depth texture with sampler
            var depthUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 3
            };
            depthUniform.AddId(_sampler);
            depthUniform.AddId(depthTexture);
            uniforms.Add(depthUniform);

            var uniformSet = _rd.UniformSetCreate(uniforms, _computeShader, 0);

            var textureFormat = _rd.TextureGetFormat(worldPosTexture);
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
