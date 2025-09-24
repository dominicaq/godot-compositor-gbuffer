using Godot;
using Godot.Collections;

public partial class RayTracer : CompositorEffect
{
    private Rid _computeShader;
    private Rid _computePipeline;
    private Rid _sampler;
    private RenderingDevice _rd;

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
        if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD renderSceneBuffers) return;

        var worldPosTexture = renderSceneBuffers.GetTexture("forward_clustered", "emission");
        var normalRoughnessTexture = renderSceneBuffers.GetTexture("forward_clustered", "normal_roughness");
        var specularTexture = renderSceneBuffers.GetTexture("forward_clustered", "specular");
        var depthTexture = renderSceneBuffers.GetDepthLayer(0);

        WorldPosPass(worldPosTexture, normalRoughnessTexture, specularTexture, depthTexture);
    }

    private void WorldPosPass(Rid worldPosTexture, Rid normalRoughnessTexture, Rid specularTexture, Rid depthTexture)
    {
        if (!_computePipeline.IsValid) return;

        var uniforms = new Array<RDUniform>();

        var worldPosUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        worldPosUniform.AddId(worldPosTexture);
        uniforms.Add(worldPosUniform);

        var normalRoughnessUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.SamplerWithTexture,
            Binding = 1
        };
        normalRoughnessUniform.AddId(_sampler);
        normalRoughnessUniform.AddId(normalRoughnessTexture);
        uniforms.Add(normalRoughnessUniform);

        var specularUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.SamplerWithTexture,
            Binding = 2
        };
        specularUniform.AddId(_sampler);
        specularUniform.AddId(specularTexture);
        uniforms.Add(specularUniform);

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
