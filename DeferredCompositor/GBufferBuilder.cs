using Godot;
using Godot.Collections;

namespace DeferredCompositor
{
    [GlobalClass]
    public partial class GBufferBuilder : CompositorEffect
    {
        public Array<Rid> GBuffer;

        public enum GBufferIndex
        {
            Color = 0,
            NormalRoughness = 1,
            Depth = 2,
        };

        public GBufferBuilder()
        {
            GBuffer = [];
            for (int i = 0; i < 4; i++)
            {
                GBuffer.Add(new Rid());
            }

            NeedsNormalRoughness = true;
            NeedsSeparateSpecular = true;
            AccessResolvedColor = true;
            AccessResolvedDepth = true;
            EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;
        }

        public override void _RenderCallback(int effectCallbackType, RenderData renderData)
        {
            if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD renderSceneBuffers)
                return;

            GBuffer[(int)GBufferIndex.Color] = renderSceneBuffers.GetColorLayer(0);
            GBuffer[(int)GBufferIndex.NormalRoughness] = renderSceneBuffers.GetTexture("forward_clustered", "normal_roughness");
            GBuffer[(int)GBufferIndex.Depth] = renderSceneBuffers.GetDepthLayer(0);
        }
    }
}
