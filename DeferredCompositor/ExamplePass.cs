using Godot;
using Godot.Collections;
using System;

namespace DeferredCompositor
{
    [GlobalClass]
    public partial class ExamplePass : CompositorEffect
    {
        private Rid _computeShader;
        private Rid _computePipeline;
        private Rid _sampler;
        private RenderingDevice _rd;

        public ExamplePass()
        {
            _rd = RenderingServer.GetRenderingDevice();
            if (_rd == null)
                return;

            CreateSampler();
            if (!SetupComputeShader())
                return;

            CreateComputePipeline();

            EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
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
            var shaderFile = GD.Load<RDShaderFile>("res://shaders/example_compute.glsl");
            if (shaderFile == null)
                return false;

            var spirv = shaderFile.GetSpirV();
            if (spirv == null)
                return false;

            _computeShader = _rd.ShaderCreateFromSpirV(spirv);
            return _computeShader.IsValid;
        }

        private void CreateComputePipeline()
        {
            if (_computeShader.IsValid)
                _computePipeline = _rd.ComputePipelineCreate(_computeShader);
        }

        public override void _RenderCallback(int effectCallbackType, RenderData renderData)
        {
            if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD rb)
                return;

            var colorTex = rb.GetColorLayer(0);
            var normalRoughnessTex = rb.GetTexture("forward_clustered", "normal_roughness");
            var depthTex = rb.GetDepthLayer(0);

            var sceneData = renderData.GetRenderSceneData();
            var proj = sceneData.GetCamProjection();
            var view = sceneData.GetCamTransform();

            RayTracingPass(colorTex, normalRoughnessTex, depthTex, proj, view);
        }

        private void RayTracingPass(Rid color, Rid normalRoughness, Rid depth,
            Projection proj, Transform3D view)
        {
            if (!_computePipeline.IsValid)
                return;

            var uniforms = new Array<RDUniform>();

            var colorU = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = 0 };
            colorU.AddId(color);
            uniforms.Add(colorU);

            var normalU = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 1 };
            normalU.AddId(_sampler);
            normalU.AddId(normalRoughness);
            uniforms.Add(normalU);

            var depthU = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 2 };
            depthU.AddId(_sampler);
            depthU.AddId(depth);
            uniforms.Add(depthU);

            var uniformSet = _rd.UniformSetCreate(uniforms, _computeShader, 0);

            var fmt = _rd.TextureGetFormat(color);
            uint xGroups = (fmt.Width + 15) / 16;
            uint yGroups = (fmt.Height + 15) / 16;

            var computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _computePipeline);
            _rd.ComputeListBindUniformSet(computeList, uniformSet, 0);

            // ---- Push constants (128 bytes: 2 mat4) ----
            var pushConstants = new byte[128];
            Buffer.BlockCopy(MatrixToBytes(proj.Inverse()), 0, pushConstants, 0, 64);
            Buffer.BlockCopy(MatrixToBytes(view.AffineInverse()), 0, pushConstants, 64, 64);
            _rd.ComputeListSetPushConstant(computeList, new ReadOnlySpan<byte>(pushConstants), (uint)pushConstants.Length);

            _rd.ComputeListDispatch(computeList, xGroups, yGroups, 1);
            _rd.ComputeListEnd();
        }

        private static byte[] MatrixToBytes(Projection m)
        {
            var f = new float[16]
            {
                m.X.X, m.X.Y, m.X.Z, m.X.W,
                m.Y.X, m.Y.Y, m.Y.Z, m.Y.W,
                m.Z.X, m.Z.Y, m.Z.Z, m.Z.W,
                m.W.X, m.W.Y, m.W.Z, m.W.W
            };
            var data = new byte[64];
            for (int i = 0; i < 16; i++)
                BitConverter.GetBytes(f[i]).CopyTo(data, i * 4);
            return data;
        }

        private static byte[] MatrixToBytes(Transform3D t)
        {
            var f = new float[16]
            {
                t.Basis.X.X, t.Basis.X.Y, t.Basis.X.Z, 0,
                t.Basis.Y.X, t.Basis.Y.Y, t.Basis.Y.Z, 0,
                t.Basis.Z.X, t.Basis.Z.Y, t.Basis.Z.Z, 0,
                t.Origin.X,  t.Origin.Y,  t.Origin.Z,  1
            };
            var data = new byte[64];
            for (int i = 0; i < 16; i++)
                BitConverter.GetBytes(f[i]).CopyTo(data, i * 4);
            return data;
        }
    }
}
