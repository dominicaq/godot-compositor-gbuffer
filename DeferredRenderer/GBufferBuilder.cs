using Godot;
using Godot.Collections;

namespace DeferredRenderer
{
    [GlobalClass]
    public partial class GBufferBuilder : CompositorEffect
    {
        public Array<Rid> GBuffer;

        public enum GBufferIndex
        {
            WorldPosition = 0,   // Absolute world position
            SignBits = 1,        // Sign bits (specular buffer)
            NormalRoughness = 2, // Normal data (normal buffer)
            Depth = 3,
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

            GBuffer[(int)GBufferIndex.WorldPosition] = renderSceneBuffers.GetColorLayer(0);
            GBuffer[(int)GBufferIndex.SignBits] = renderSceneBuffers.GetTexture("forward_clustered", "specular");
            GBuffer[(int)GBufferIndex.NormalRoughness] = renderSceneBuffers.GetTexture("forward_clustered", "normal_roughness");
            GBuffer[(int)GBufferIndex.Depth] = renderSceneBuffers.GetDepthLayer(0);
        }
    }
}
