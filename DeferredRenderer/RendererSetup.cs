using Godot;

namespace DeferredRenderer
{
    public partial class RendererSetup : WorldEnvironment
    {
        private GBufferBuilder _gBufferBuilder;
        private RayTracer _rayTracer;

        public override void _Ready()
        {
            SetupDeferredRenderer();
        }

        private void SetupDeferredRenderer()
        {
            // Create a new Compositor if one doesn't exist
            Compositor ??= new Compositor();

            // Create the effects
            _gBufferBuilder = new GBufferBuilder();
            _rayTracer = new RayTracer();

            // Link them together
            // _rayTracer.SetGBufferBuilder(_gBufferBuilder);

            var compositorEffects = Compositor.CompositorEffects;
            compositorEffects.Resize(2);
            compositorEffects[0] = _gBufferBuilder;
            compositorEffects[1] = _rayTracer;
            GD.Print($"Added {compositorEffects.Count} effects to compositor");
            GD.Print($"GBuffer effect callback type: {_gBufferBuilder.EffectCallbackType}");
            GD.Print($"RayTracer effect callback type: {_rayTracer.EffectCallbackType}");
            GD.Print("Deferred renderer setup complete");
        }
    }
}
